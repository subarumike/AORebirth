namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.NpcEditor;

[TestClass]
public sealed class NpcEditorLootEvidenceTests
{
    const string Actor = "Evidence GM";

    [TestMethod]
    public void LinkedCaptureImportPreservesItemPairAndProvenanceAndDeduplicatesReimports()
    {
        using var fixture = new Fixture();
        const string capture = "20260717-214751";
        const string csv = """
            CapturedUtc,Direction,Sequence,CorpseIdentity,OpenOrdinal,InitialSnapshot,ItemCount,DeadNpcIdentity,EnemyName,MonsterData,EnemyLevel,CorpseCredits,PlayerIdentity,PlayerLevel,PlayfieldId,Items,CorrelationStatus
            2026-07-18T02:48:25.9344451Z,IN-N3,442,(Corpse:F69010),1,true,3,(SimpleChar:79748428),Eumenides,203726,20,186,,25,1407006,163430:163431:22:1;301714:301714:1:1;287146:287146:200:1,linked-offline-generation
            2026-07-18T02:48:26Z,IN-N3,443,(Corpse:F69010),2,false,1,(SimpleChar:79748428),Eumenides,203726,20,186,,25,1407006,287146:287146:200:1,linked-offline-generation
            """;
        var import = new LootEvidenceImport(0, capture, "corpse-loot-observations.csv", "aosharp", csv);
        LootEvidenceImportResult result = fixture.Evidence.Import(import, Actor);
        Assert.AreEqual(3, result.Imported);
        Assert.AreEqual(1, result.Skipped);
        Assert.AreEqual(3, fixture.Evidence.Import(import with { ExpectedRevision = 1 }, Actor).Duplicates);
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(() => fixture.Evidence.Import(import, Actor)).StatusCode);

        var reopened = new NpcEditorLootEvidence(fixture.Root, fixture.Overlay, new StubLogger());
        LootEvidencePage page = reopened.Read("163431", null, null, 0, 10);
        LootEvidenceObservation row = page.Rows.Single();
        Assert.AreEqual("OBSERVED_AVAILABLE_LOOT", row.Classification);
        Assert.AreEqual(capture, row.CaptureSource);
        Assert.AreEqual(203726, row.MonsterData);
        Assert.AreEqual(1407006, row.PlayfieldId);
        Assert.AreEqual(22, row.Quality);
        Assert.AreEqual(1, row.ObservationCount);
        Assert.AreEqual("(SimpleChar:79748428)", row.EnemyIdentity);
        Assert.AreEqual("442/(Corpse:F69010)/1", row.SourceRow);
        Assert.AreEqual(DateTimeOffset.Parse("2026-07-18T02:48:25.9344451Z"), row.CapturedUtc);
        Assert.IsNull(row.TemplateHash);
        Assert.AreEqual(64, row.FileFingerprint.Length);
    }

    [TestMethod]
    public void ImportedGuaranteeClaimsStayUnresolvedAndReviewCannotActivateLoot()
    {
        using var fixture = new Fixture();
        const string json = """
            [{"enemyName":"Coverage mob","enemyIdentity":"Original NPC ID","templateHash":"CO01","lowId":1001,"highId":1001,
              "quality":20,"observationCount":3,"patch":"18.7.0","classification":"GUARANTEED_PROVEN","sourceRow":"row-1"}]
            """;
        fixture.Evidence.Import(new(0, "reviewed source", "observations.json", "json", json), Actor);
        LootEvidenceObservation row = fixture.Evidence.Snapshot().Rows.Single();
        Assert.AreEqual("UNRESOLVED", row.Classification);
        Assert.AreEqual("GUARANTEED_PROVEN", row.ClaimedClassification);
        Assert.AreEqual("18.7.0", row.Patch);
        Assert.AreEqual(3, row.ObservationCount);
        LootEvidenceObservation approved = fixture.Evidence.Review(new(1, row.Id, "approved"), Actor);
        Assert.AreEqual("approved", approved.Review);
        Assert.AreEqual(Actor, approved.ReviewedBy);
        Assert.IsNotNull(approved.ReviewedUtc);
        Assert.AreEqual("UNRESOLVED", approved.Classification);
        Assert.AreEqual(0, fixture.Named.Describe().Revision);
        Assert.IsFalse(fixture.Named.Describe().Tables.Any());
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            fixture.Evidence.Review(new(1, row.Id, "rejected"), Actor)).StatusCode);
    }

    [TestMethod]
    public void CoverageDistinguishesUnconfiguredNativeLootAndMissingObservationAndCanExport()
    {
        using var fixture = new Fixture();
        const string json = """
            [{"enemyName":"Coverage mob","templateHash":"CO01","playfieldId":777,"level":20,"lowId":1002,"highId":1002,
              "quality":20,"classification":"OBSERVED_AVAILABLE_LOOT","sourceRow":"row-2"}]
            """;
        fixture.Evidence.Import(new(0, "exact evidence fixture", "observations.json", "json", json), Actor);
        var coverage = new NpcEditorLootCoverage(fixture.Data, fixture.Items, fixture.Named, fixture.Evidence);
        var query = new LootCoverageQuery(null, null, null, null, null, null, null, null, 0, 20);
        LootCoveragePage all = coverage.Read(query, CancellationToken.None);
        Assert.IsTrue(all.Rows.Any(row => row.Kind == "Template band" && row.Key == "CO02/0" && row.Status == "UNCONFIGURED"));
        Assert.IsTrue(all.Rows.Any(row => row.Kind == "Authored point" && row.Key == "777/0/CO01/0" && row.Status == "CONFIGURED"));
        Assert.IsTrue(all.Rows.Any(row => row.Kind == "Observation" && row.Status == "OBSERVATION_NOT_CONFIGURED"));
        LootCoveragePage exported = coverage.Read(query with { PlayfieldId = 777, Limit = 1 }, CancellationToken.None, export: true);
        Assert.AreEqual(2, exported.Rows.Length);

        var input = new LootComparisonInput("CO01", "Coverage mob", null, 20, 777,
            [new("Native", "ITEM", 100, 1, 0, 20, 20, "", "", [new("ITEM", "Known item", [1001])], "", 1, 1, [])], []);
        LootComparisonResult compared = coverage.Compare(input);
        Assert.IsTrue(compared.Rows.Any(row => row.LowId == 1002 && row.Status == "OBSERVATION_NOT_CONFIGURED"));
        Assert.IsTrue(compared.Rows.Any(row => row.LowId == 1001 && row.Status == "CONFIGURED_WITHOUT_EXACT_EVIDENCE"));
    }

    [TestMethod]
    public void CoverageScansSelectedReadOnlyGameDataWithoutAnOverlay()
    {
        string? root = Environment.GetEnvironmentVariable("AO_REBIRTH_NPC_EDITOR_VERIFY_ITEMS_ROOT");
        if (string.IsNullOrWhiteSpace(root))
            Assert.Inconclusive("Set AO_REBIRTH_NPC_EDITOR_VERIFY_ITEMS_ROOT to the selected GameData root.");
        var logger = new StubLogger();
        var data = new GameDataStore(logger, null, root!, null);
        var items = new ItemTemplateCatalog(data, logger);
        var evidence = new NpcEditorLootEvidence(root!, null, logger);
        var coverage = new NpcEditorLootCoverage(data, items, new NpcEditorNamedLoot(data, items), evidence);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var query = new LootCoverageQuery(null, null, null, null, null, null, null, null, 0, 200);
        LootCoveragePage page = coverage.Read(query, deadline.Token);
        Assert.IsTrue(page.Total > data.MobTemplateCount, "The scan should include authored points as well as catalog bands.");
        Assert.IsTrue(page.Rows.Any(row => row.Kind == "Template band"));
    }

    sealed class Fixture : IDisposable
    {
        readonly string folder = Directory.CreateTempSubdirectory("aorebirth-loot-evidence-").FullName;
        internal string Root => Path.Combine(folder, "GameData");
        internal string Overlay => Path.Combine(folder, "Overlay");
        internal StubCatalog Items { get; } = new StubCatalog().Add(1001, 20).Add(1002, 20);
        internal GameDataStore Data { get; }
        internal NpcEditorLootEvidence Evidence { get; }
        internal NpcEditorNamedLoot Named { get; }

        internal Fixture()
        {
            string placement = Path.Combine(Root, GameDataPaths.PlayfieldSpawnsRelativePath(777));
            Directory.CreateDirectory(Path.GetDirectoryName(placement)!);
            Directory.CreateDirectory(Overlay);
            File.WriteAllText(Path.Combine(Root, GameDataPaths.NpcTemplatesFileName), """
                {"CO01":{"Templates":[{"Name":"Coverage mob","Level":20,"LootTable":[{"Hash":"ITEM","Chance":100,"Repeats":1}]}]},
                 "CO02":{"Templates":[{"Name":"No configured loot","Level":20}]}}
                """);
            File.WriteAllText(Path.Combine(Root, GameDataPaths.ItemTemplatesFileName), """{"ITEM":{"Templates":[1001]}}""");
            File.WriteAllText(placement, JsonSerializer.Serialize(new PlayfieldSpawnsData
            {
                PlayfieldId = 777, SchemaVersion = PlayfieldSpawnsData.SupportedSchemaVersion,
                Spawns = [new PlayfieldSpawnEntry
                {
                    HashText = "CO01", Position = [10, 0, 20], MinLevel = 20, MaxLevel = 20, RespawnTime = 30, RespawnChance = 100
                }]
            }));
            var logger = new StubLogger();
            Data = new(logger, null, Root, Overlay);
            Named = new(Data, Items);
            Evidence = new(Root, Overlay, logger);
        }

        public void Dispose() => Directory.Delete(folder, recursive: true);
    }
}
