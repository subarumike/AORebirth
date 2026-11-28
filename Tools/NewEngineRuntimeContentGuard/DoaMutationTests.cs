using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class DoaMutationTests
{
    private const string MissionPath = "AORebirth/Server/ZoneEngine_New/Core/Missions/GuardProbe.cs";
    private const string Generation = """
        {
          "FindItem": { "ItemHashes": ["FIND"] },
          "ReturnItem": { "ItemHashes": ["RETN"] },
          "RepairMachine": { "Source": { "ItemHashes": ["TOOL"] }, "Target": { "ItemHashes": ["FIXD"] } },
          "NpcObjectives": { "NpcHashes": ["BOSS"], "FindPersonNpcHashes": ["BOSS"] },
          "Population": { "RegularNpcHashes": ["BOSS"] },
          "ContentCoverage": {
            "FindItem": { "Status": "Provisional", "Gap": "More verified objective variants" },
            "ReturnItem": { "Status": "Provisional", "Gap": "More verified objective variants" },
            "RepairMachine": { "Status": "Provisional", "Gap": "More verified pairs" },
            "KillPerson": { "Status": "Provisional", "Gap": "More verified NPC families" },
            "FindPerson": { "Status": "Provisional", "Gap": "More verified NPC families" },
            "Population": { "Status": "Provisional", "Gap": "Verified family coverage by band" }
          }
        }
        """;

    internal static int Run(string root)
    {
        var cases = new (string Name, Action Check)[]
        {
            ("literal item identity", () => Block("class Test { const int KeyLowId = 3210; }", "DOA_LITERAL_CONTENT_IDENTITY")),
            ("direct content resolver", () => Block("class Test { bool Run() => data.TryResolveMobTemplate(\"F001\", ql, out _); }", "DOA_LITERAL_CONTENT_CALL")),
            ("inline objective pool", () => Block("class Test { string[] NpcHashes = [\"F001\"]; }", "DOA_FIXED_MISSION_POLICY")),
            ("content identity pattern", () => Block("class Test { bool Run(int itemLowId) => itemLowId is 3210; }", "DOA_LITERAL_CONTENT_BRANCH")),
            ("fixed floor parameter", () => Block("class Test { int FloorCount = 2; }", "DOA_FIXED_MISSION_POLICY")),
            ("silent objective fallback", () => Block("class Test { object Run() => row.NpcHash ?? \"F001\"; }", "DOA_FALLBACK_CONTENT")),
            ("recorded packet dependency", () => Block("class Test { object Run() => GeneratedMissionWire.CapturedBody(0); }", "DOA_RECORDED_PACKET_DEPENDENCY")),
            ("offer wire reader boundary", CheckOfferWireReader),
            ("typed wire contract", () => Require(!Rules("enum WireCode { Quest = 0xDAC3 } class Test { int Read() => row.GetInt32(5); }").Any())),
            ("mission persistence boundary", () => Block("using MySqlConnector; class Test { object Open() => new MySqlConnection(); }", "DOA_MODULE_DEPENDENCY")),
            ("direct database access boundary", CheckDatabaseAccess),
            ("concrete DAO ownership", CheckConcreteDaoOwnership),
            ("gameplay dispatch boundary", CheckGameplayDispatch),
            ("competing persistence contract", CheckCompetingPersistence),
            ("mission transaction service boundary", CheckTransactionBoundary),
            ("reward dispatch and recovery boundary", CheckRewardEffects),
            ("reward transaction and applied marker boundary", CheckRewardTransactions),
            ("single reward ledger and execution module", CheckRewardOwner),
            ("reward GameData authority and values", CheckRewardContent),
            ("internal module dependencies", CheckModuleDependencies),
            ("project dependency boundary", CheckProjects),
            ("unchanged and introduced findings", CheckDiff),
            ("versioned layout exception", () => CheckHistoricalLayout(root)),
            ("direct catalog source dependencies", CheckDirectContentSources),
            ("single runtime content authority", CheckContentAuthority),
            ("unauthorized fallback catalogs", CheckCatalogFallback),
            ("required content validation boundary", CheckContentValidation),
            ("provisional catalog validation", CheckCoverage),
            ("recorded payload field", CheckRecordedPayload),
            ("key catalog validation", CheckKeyCatalog),
            ("authoritative references", CheckReferences)
        };
        int passed = 0;
        foreach (var test in cases)
        {
            try
            {
                test.Check();
                Console.WriteLine("PASS " + test.Name);
                passed++;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL " + test.Name + " " + exception.Message);
            }
        }
        Console.WriteLine($"DOA_MUTATION_TESTS={passed}/{cases.Length}");
        return passed == cases.Length ? 0 : 1;
    }

    private static void Block(string source, string rule) => Require(Rules(source).Contains(rule));
    private static IEnumerable<string> Rules(string source) => DoaRules.SourceIssues(MissionPath, Parse(source)).Select(issue => issue.Rule);
    private static SyntaxNode Parse(string source) => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected architecture check did not hold.");
    }

    private static void CheckOfferWireReader()
    {
        const string rollService = "AORebirth/Server/ZoneEngine_New/Core/Missions/GeneratedMissionRollService.cs";
        const string offerCatalog = "AORebirth/Server/ZoneEngine_New/Core/Missions/Content/MissionOfferCatalog.cs";
        const string dao = "AORebirth/Libraries/Source/AORebirth.Database/Domain/Quests/MySqlCharacterQuestDao.cs";
        var read = Parse("class Test { object Run() => GeneratedMissionWire.Read(renamedInput); }");
        Require(DoaRules.SourceIssues(rollService, read).Any(issue => issue.Rule == "DOA_RECORDED_PACKET_DEPENDENCY"));
        Require(DoaRules.SourceIssues(offerCatalog, read).Any(issue => issue.Rule == "DOA_RECORDED_PACKET_DEPENDENCY"));
        Require(!DoaRules.SourceIssues(dao, read).Any());
        Require(!DoaRules.SourceIssues(rollService, Parse("class Test { object Run() => GeneratedMissionWire.Write(typedOffer); }")).Any());
    }

    private static void CheckProjects()
    {
        Require(DoaRules.IsForbiddenRuntimeProjectReference("Tools/Probe/Probe.csproj"));
        Require(DoaRules.IsForbiddenRuntimeProjectReference("AORebirth/Server/ZoneEngine_New.Tests/ZoneEngine_New.Tests.csproj"));
        Require(DoaRules.IsForbiddenRuntimeProjectReference("AORebirth/Server/WorldEngine/WorldEngine.csproj"));
        Require(!DoaRules.IsForbiddenRuntimeProjectReference("AORebirth/Libraries/Source/AORebirth.Modules.Abstractions/AORebirth.Modules.Abstractions.csproj"));
    }

    private static void CheckDatabaseAccess()
    {
        Block("""class Probe { object Load() => storage.Execute("SELECT * FROM MissionOffers"); }""", "DOA_DIRECT_DATABASE_ACCESS");
        Block("""class Probe { string Write() => $"UPDATE MissionOffers SET Fee = {fee}"; }""", "DOA_DIRECT_DATABASE_ACCESS");
        Block("class Probe { object Open() => Connector.GetConnection(); }", "DOA_DIRECT_DATABASE_ACCESS");
        Block("class Probe { object Start() => connection.BeginTransaction(); }", "DOA_DIRECT_DATABASE_ACCESS");
        Block("using Connection = Npgsql.NpgsqlConnection; class Probe { object Open() => new Connection(cs); }", "DOA_DIRECT_DATABASE_ACCESS");

        const string dao = "AORebirth/Libraries/Source/AORebirth.Database/Domain/Missions/DaoProbe.cs";
        Require(!DoaRules.SourceIssues(dao,
            Parse("""using Dapper; class Probe { object Load() => connection.Query("SELECT * FROM MissionOffers"); }""")).Any());
        Require(!Rules("class Probe { object Read() => world.Query<Cell>(key); }").Any());
    }

    private static void CheckConcreteDaoOwnership()
    {
        Block("class Probe { object Data() => new MySqlMissionDao(factory); }", "DOA_CONCRETE_DAO");
        Block("class Probe(MySqlMissionDao dao) {}", "DOA_CONCRETE_DAO");
        Block("using Data = AORebirth.Database.Domain.Missions.MySqlMissionDao; class Probe { Data dao = new(factory); }", "DOA_CONCRETE_DAO");
        Require(!Rules("class Probe(IGeneratedMissionDao dao) { object Data() => dao.ReserveIdentities(sequence, count); }").Any());

        const string composition = "AORebirth/Server/ZoneEngine_New/Program.cs";
        Require(!DoaRules.SourceIssues(composition,
            Parse("class Program { void BuildRootServices() { services.AddSingleton<IGeneratedMissionDao>(_ => new MySqlMissionDao(() => new MySqlConnection(settings))); } }")).Any());
        Require(DoaRules.SourceIssues(composition,
            Parse("""class Program { void BuildRootServices() { services.AddSingleton(_ => connection.Execute("DELETE FROM MissionOffers")); } }"""))
            .Any(issue => issue.Rule == "DOA_DIRECT_DATABASE_ACCESS"));
    }

    private static void CheckGameplayDispatch()
    {
        const string handlers = "AORebirth/Server/ZoneEngine_New/Core/MessageHandlers/";
        const string dispatch = "class Probe(IMissionTerminalModule module) { public void Handle(QuestAlternativeMessage message, IZoneSession session) => module.Roll(message, session); }";
        Require(!DoaRules.SourceIssues(handlers + "QuestAlternativeMessageHandler.cs", Parse(dispatch)).Any());
        string withPolicy = dispatch.Replace("=> module.Roll(message, session);", "{ if (Flag()) module.Roll(message, session); }", StringComparison.Ordinal);
        Require(DoaRules.SourceIssues(handlers + "QuestAlternativeMessageHandler.cs", Parse(withPolicy))
            .Any(issue => issue.Rule == "DOA_GAMEPLAY_IN_DISPATCH"));
        Require(DoaRules.SourceIssues(handlers + "OtherHandler.cs", Parse("class Probe { void Handle() => player.ApplyDamage(amount); }"))
            .Any(issue => issue.Rule == "DOA_GAMEPLAY_IN_DISPATCH"));
        Require(DoaRules.SourceIssues("AORebirth/Server/ZoneEngine_New/Program.cs", Parse("class Program { void Run() => rewards.GrantReward(reward); }"))
            .Any(issue => issue.Rule == "DOA_GAMEPLAY_IN_DISPATCH"));
    }

    private static void CheckCompetingPersistence()
    {
        Block("interface IMissionRepository { void Save(); }", "DOA_COMPETING_PERSISTENCE");
        Block("interface ITerminalQuestSpareDao { void Save(); }", "DOA_COMPETING_PERSISTENCE");
        Block("class AddedDbContext : DbContext {}", "DOA_COMPETING_PERSISTENCE");
        Require(!Rules("interface IMissionTerminalModule { void Roll(); }").Any());
    }

    private static void CheckTransactionBoundary()
    {
        Block("class Probe(IGeneratedMissionDao dao) { object Run() => dao.PublishOffers(batch); }", "DOA_TRANSACTION_BYPASS");
        Require(!Rules("class Probe(GeneratedMissionService missions) { object Run() => missions.PublishOffers(player, batch); }").Any());
        const string owner = "AORebirth/Server/ZoneEngine_New/Core/Missions/GeneratedMissionService.cs";
        Require(!DoaRules.SourceIssues(owner,
            Parse("class GeneratedMissionService(IGeneratedMissionDao dao) { object PublishOffers() => dao.PublishOffers(batch); }")).Any());
        Require(DoaRules.SourceIssues(owner,
            Parse("class GeneratedMissionService(IGeneratedMissionDao dao) { object Bypass() => dao.PublishOffers(batch); }"))
            .Any(issue => issue.Rule == "DOA_TRANSACTION_BYPASS"));
        Block("class Probe(IInventoryMutationPersistence inventory) { void Run() => inventory.Persist(batch); }", "DOA_TRANSACTION_BYPASS");
        Block("class Probe { void Run() => services.GetRequiredService<ITradePersistence>().Persist(batch); }", "DOA_TRANSACTION_BYPASS");
        Require(!DoaRules.SourceIssues("AORebirth/Server/ZoneEngine_New/Core/Inventory/InventoryActionService.cs",
            Parse("class InventoryActionService(IInventoryMutationPersistence persistence) { void TryCommit() => persistence.Persist(batch); }")).Any());
        Require(!DoaRules.SourceIssues("AORebirth/Server/ZoneEngine_New/Core/Trade/TradeService.cs",
            Parse("class TradeService { ITradePersistence persistence; void PersistPlan() => this.persistence.Persist(batch); }")).Any());
        Require(!Rules("class Probe { void Run() => cache.Persist(snapshot); }").Any());
    }

    private static void CheckRewardEffects()
    {
        const string handler = "AORebirth/Server/ZoneEngine_New/Core/MessageHandlers/RewardHandler.cs";
        Require(DoaRules.SourceIssues(handler, Parse("class Probe { void Run() => player?.AwardXp(amount); }"))
            .Any(issue => issue.Rule == "DOA_GAMEPLAY_IN_DISPATCH"));
        Require(!DoaRules.SourceIssues(handler,
            Parse("class Probe(IRewardExecutionModule rewards) { void Run() => rewards.Execute(request); }")).Any());

        string[] unsafeCalls =
        [
            "player.ApplyResearchXp(plan.ResearchXp)", "player.AddToDeathXpPool(plan.PooledXp)",
            "item.Definition.ExecuteOnUseSpells(player, inventory, builder)", "quest.TryAssignTemplate(player, next, out _)"
        ];
        foreach (string call in unsafeCalls)
            Require(DoaRules.SourceIssues(DoaRewardBoundary.ModulePath,
                Parse($"class RewardExecutionModule {{ void Run() => {call}; }}"))
                .Any(issue => issue.Rule == "DOA_UNTRACKED_REWARD_EFFECT"));
    }

    private static void CheckRewardTransactions()
    {
        Block("class Probe(ICharacterQuestCompletionTransaction tx) { object Run() => tx.TryCompleteCharacterQuest(request); }",
            "DOA_REWARD_TRANSACTION_BYPASS");
        Block("class Probe(IMissionDaoTransaction tx) { object Run() => tx.TryApplyCharacterStatRewardWithResearch(plan); }",
            "DOA_REWARD_TRANSACTION_BYPASS");
        Block("class Probe(IMissionDaoTransaction tx) { void Run() { var mark = tx.TryMarkRewardApplied; mark(key); } }",
            "DOA_REWARD_TRANSACTION_BYPASS");
        Block("class Probe(IMissionDaoTransaction tx) { void Run() => tx.TryClaimReward(key); }", "DOA_REWARD_TRANSACTION_BYPASS");
        Block("class Probe(ICharacterQuestCompletionTransaction tx) { void Run() => tx.TryApplyCharacterQuestFollowUp(request); }",
            "DOA_REWARD_TRANSACTION_BYPASS");

        const string coordinated = "class RewardExecutionModule(IMissionDao? dao) { object Run() => dao.Execute(owner, account, tx => Complete(tx).TryCompleteCharacterQuest(request)); }";
        Require(!DoaRules.SourceIssues(DoaRewardBoundary.ModulePath, Parse(coordinated)).Any());
        Require(DoaRules.SourceIssues(DoaRewardBoundary.ModulePath,
            Parse(coordinated.Replace("IMissionDao? dao", "OtherRunner dao", StringComparison.Ordinal)))
            .Any(issue => issue.Rule == "DOA_REWARD_TRANSACTION_BYPASS"));
        Require(!DoaRules.SourceIssues(DoaRewardBoundary.FollowUpPath, Parse(
            "class QuestService { object TryApplyQuestFollowUp() => _completionDao!.Execute(owner, tx => Complete(tx).TryApplyCharacterQuestFollowUp(request)); }")).Any());
    }

    private static void CheckRewardOwner()
    {
        Block("interface IAlternateRewardLedger { void Write(); }", "DOA_COMPETING_REWARD_LEDGER");
        Block("class LocalCompletion : ICharacterQuestCompletionTransaction {}", "DOA_COMPETING_REWARD_LEDGER");
        Block("class AlternateExecution : IRewardExecutionModule {}", "DOA_DUPLICATE_REWARD_EXECUTION");
        Block("interface IRewardExecutionModule {}", "DOA_DUPLICATE_REWARD_EXECUTION");
        Require(!DoaRules.SourceIssues(DoaRewardBoundary.ModulePath,
            Parse("interface IRewardExecutionModule {} class RewardExecutionModule : IRewardExecutionModule {}")).Any());
        Require(DoaRules.SourceIssues(DoaRewardBoundary.ModulePath,
            Parse("class RewardExecutionModule(MySqlMissionDao dao) { void Run() {} }"))
            .Any(issue => issue.Rule == "DOA_CONCRETE_DAO"));
        Require(DoaRules.SourceIssues(DoaRewardBoundary.ModulePath,
            Parse("""class RewardExecutionModule { void Run() => db.Execute("UPDATE characters SET Cash = 1"); }"""))
            .Any(issue => issue.Rule == "DOA_DIRECT_DATABASE_ACCESS"));
    }

    private static void CheckRewardContent()
    {
        Block("class Probe { object Run() => new QuestReward { Credits = 150, Xp = -10 }; }", "DOA_FIXED_REWARD_VALUE");
        Block("class Probe { object Run() => new QuestRewardItem { Id = 1500 }; }", "DOA_FIXED_REWARD_VALUE");
        Block("class Probe { QuestRewardItem item = new() { Quality = 5 }; }", "DOA_FIXED_REWARD_VALUE");
        Block("class Probe { object Run() => DirectXpRewardPlan.CreateQuestReward(player, 500); }", "DOA_FIXED_REWARD_VALUE");
        Block("class Probe { int Run() => data?.RewardXp ?? 500; }", "DOA_FIXED_REWARD_VALUE");
        Block("class Probe { const double RewardWeight = 0.2; }", "DOA_FIXED_REWARD_VALUE");
        Block("""class Probe { object Load() => File.ReadAllText("Rewards.json"); }""", "DOA_DIRECT_CONTENT_SOURCE");
        Require(!Rules("class Probe { object Run() => new QuestReward { Credits = reward.Credits, Xp = plan.Xp }; }").Any());
    }

    private static void CheckModuleDependencies()
    {
        Block("class Probe(InventoryModule inventory) {}", "DOA_UNAUTHORIZED_MODULE_DEPENDENCY");
        Block("using InventoryOwner = ZoneEngine_New.Core.Inventory.InventoryModule; class Probe { object Run() => new InventoryOwner(); }",
            "DOA_UNAUTHORIZED_MODULE_DEPENDENCY");
        Block("using ZoneEngine_New.Core.Quests.Internal; class Probe {}", "DOA_UNAUTHORIZED_MODULE_DEPENDENCY");
        Block("class Probe(QuestDungeonService quest) {}", "DOA_UNAUTHORIZED_MODULE_DEPENDENCY");
        Require(!Rules("class Probe(IInventoryModule inventory, MissionTerminalModule ownModule, InventoryFlushService shared, IZoneSession session) {}").Any());
    }

    private static void CheckDiff()
    {
        Finding old = DoaRules.DataFinding(MissionPath, "Probe", "EXAMPLE", "Open debt", "old");
        Finding changed = DoaRules.DataFinding(MissionPath, "Probe", "EXAMPLE", "Open debt", "changed");
        var result = DoaEnforcement.Compare([old], [old with { Line = 200 }, old, changed]);
        Require(result.Inherited.Length == 1 && result.Introduced.Length == 2);
    }

    private static void CheckDirectContentSources()
    {
        Block("class Probe { string Read() => File.ReadAllText(path); }", "DOA_DIRECT_CONTENT_SOURCE");
        Block("class Probe { string Read() => GameDataPaths.ResolveRuntimeRoot(); }", "DOA_DIRECT_CONTENT_SOURCE");
        Block("using Paths = AORebirth.Core.GameData.GameDataPaths; class Probe { string Read() => Paths.ResolveFile(root, name); }",
            "DOA_DIRECT_CONTENT_SOURCE");
        Block("using static System.IO.File; class Probe { string Read() => ReadAllText(path); }", "DOA_DIRECT_CONTENT_SOURCE");
        Block("class Probe { object Scan() => Directory.EnumerateFiles(root); }", "DOA_DIRECT_CONTENT_SOURCE");
        Block("class Probe { object Read() => new StreamReader(path); }", "DOA_DIRECT_CONTENT_SOURCE");
        Block("""class Probe { string Read() => Environment.GetEnvironmentVariable("AO_REBIRTH_GAMEDATA_PATH"); }""",
            "DOA_DIRECT_CONTENT_SOURCE");

        const string otherGameplay = "AORebirth/Server/ZoneEngine_New/Core/Mobs/GuardProbe.cs";
        Require(DoaRules.SourceIssues(otherGameplay, Parse("""class Probe { object Read() => File.OpenRead("Mobs.json"); }"""))
            .Any(issue => issue.Rule == "DOA_DIRECT_CONTENT_SOURCE"));
        const string combatModule = "AORebirth/Server/ZoneEngine_New/Core/Combat/GuardProbe.cs";
        Require(DoaRules.SourceIssues(combatModule, Parse("class Probe { object Read(string path) => File.ReadAllText(path); }"))
            .Any(issue => issue.Rule == "DOA_DIRECT_CONTENT_SOURCE"));
        Require(!DoaRules.SourceIssues("AORebirth/Server/ZoneEngine_New/Core/Metrics/GuardProbe.cs",
            Parse("""class Probe { object Read() => File.OpenRead("spawn.log"); }""")).Any());
        Require(!Rules("class Probe { object Read() => new StreamReader(new MemoryStream(bytes)); }").Any());

        string Fingerprint(string body) => DoaRules.SourceFingerprint(DoaRules.SourceIssues(MissionPath, Parse(body)).Single().Node,
            DoaContentBoundary.DirectSourceRule);
        Require(Fingerprint("class Probe { object Read() => File.ReadAllText(path); }")
            == Fingerprint("class Probe { object Read() => Validate(File.ReadAllText(path)); }"));
        Require(Fingerprint("class Probe { object Read() => File.ReadAllText(path); }")
            != Fingerprint("class Probe { object Read() => File.ReadAllText(fallbackPath); }"));
    }

    private static void CheckContentAuthority()
    {
        Block("class Probe : IGameDataContentProvider {}", "DOA_DUPLICATE_CONTENT_AUTHORITY");
        Block("class Probe { GameDataContentProvider Source = new(root); }", "DOA_DUPLICATE_CONTENT_AUTHORITY");
        const string provider = "AORebirth/Server/ZoneEngine_New/Core/GameData/GameDataContentProvider.cs";
        Require(!DoaRules.SourceIssues(provider, Parse("class GameDataContentProvider { object Open() => File.ReadAllBytes(root); }")).Any());
        Require(DoaRules.SourceIssues(provider, Parse("class Other { object Build() => new GameDataContentProvider(root); }"))
            .Any(issue => issue.Rule == "DOA_DUPLICATE_CONTENT_AUTHORITY"));
    }

    private static void CheckCatalogFallback()
    {
        Block("""class Probe { string PathFor() => File.Exists(approved) ? approved : Path.Combine(packaged, "Offers.json"); }""",
            "DOA_UNAUTHORIZED_CATALOG_FALLBACK");
        Block("""class Probe { object Read() { if (File.Exists(approved)) return Read(approved); else return Read("Offers.json"); } }""",
            "DOA_UNAUTHORIZED_CATALOG_FALLBACK");
        Block("""class Probe { string PathFor() => Path.Combine(Environment.CurrentDirectory, "Offers.json"); }""",
            "DOA_UNAUTHORIZED_CATALOG_FALLBACK");
    }

    private static void CheckContentValidation()
    {
        Block("class Probe { object Build() => new ContentSnapshot<Offers>(data); }", "DOA_CONTENT_VALIDATION_BYPASS");
        Block("class Probe { object Build(ContentInputs input) { try { return Parse(input.Required(offerFile)); } catch { return new Offers(); } } }",
            "DOA_CONTENT_VALIDATION_BYPASS");
        Block("class Probe { object Build(ContentInputs input) { try { return input.RequireDependency<Keys>(keysId); } catch { return new Keys(); } } }",
            "DOA_CONTENT_VALIDATION_BYPASS");
        Require(!Rules("class Probe { object Build(ContentInputs input) => Validate(Parse(input.Required(offerFile))); }").Any());
        Require(!Rules("class Probe { object Current(ContentSlot<Offers> slot) => slot.Current; }").Any());
    }

    private static void CheckHistoricalLayout(string root)
    {
        string source = File.ReadAllText(Path.Combine(root, DoaRules.LayoutPath));
        Require(DoaRules.IsHistoricalLayoutContract(Parse(source)));
        Require(!DoaRules.SourceIssues(DoaRules.LayoutPath, Parse(source)).Any(issue => issue.Rule == "DOA_FIXED_MISSION_POLICY"));
        foreach (int otherVersion in new[] { 1, 3 })
        {
            string changed = source.Replace("version != 2 ||", $"(version != 2 && version != {otherVersion}) ||", StringComparison.Ordinal);
            Require(!DoaRules.IsHistoricalLayoutContract(Parse(changed)));
            Require(DoaRules.SourceIssues(DoaRules.LayoutPath, Parse(changed)).Any(issue => issue.Rule == "DOA_FIXED_MISSION_POLICY"));
        }
    }

    private static void CheckCoverage()
    {
        Require(!Catalog(Generation).Any(finding => finding.Path.EndsWith("DungeonGeneration.json", StringComparison.Ordinal)));
        string invalid = Generation.Replace("\"Provisional\"", "\"Complete\"", StringComparison.Ordinal);
        Require(Catalog(invalid).Any(finding => finding.Rule == "DOA_UNSUPPORTED_COMPLETENESS"));
        string noPool = Generation.Replace("[\"FIND\"]", "[]", StringComparison.Ordinal);
        Require(Catalog(noPool).Any(finding => finding.Rule == "DOA_INVALID_GAMEPLAY_POOL"));
    }

    private static void CheckRecordedPayload()
    {
        var typed = new
        {
            FormatVersion = 1, Provenance = new { InputSha256 = new string('a', 64), SourceRevision = new string('b', 40) },
            Responses = new[] { new { BodySha256 = new string('c', 64), Offers = Enumerable.Range(0, 5).Select(index => new { Index = index }) } }
        };
        string json = JsonSerializer.Serialize(typed);
        Require(!Catalog(Generation, offers: json).Any(finding => finding.Path.EndsWith("MissionOffers.json", StringComparison.Ordinal)));
        Require(Catalog(Generation, offers: json.Insert(1, "\"BodyHex\":\"abcd\",")).Any(finding => finding.Rule == "DOA_RECORDED_PACKET_CATALOG"));
    }

    private static void CheckKeyCatalog()
    {
        const string keys = """{"Version":1,"KeyTemplateId":100,"DuplicatorTemplateId":200,"KeyQuality":1,"MaxKeysPerQuest":2,"NamePrefix":"Key to "}""";
        Require(!Catalog(Generation, keys: keys).Any(finding => finding.Path.EndsWith("Keys.json", StringComparison.Ordinal)));
        Require(Catalog(Generation, keys: keys.Replace("200", "100", StringComparison.Ordinal)).Any(finding => finding.Rule == "DOA_INVALID_MISSION_KEYS"));
    }

    private static IEnumerable<Finding> Catalog(string generation, string? offers = null, string? keys = null)
        => DoaRules.CatalogIssues(path => path switch
        {
            "DungeonGeneration.json" => generation,
            "Missions/MissionOffers.json" => offers,
            "Missions/Keys.json" => keys,
            _ => null
        });

    private static void CheckReferences()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("aorebirth-doa-test-");
        string generation = Path.Combine(root.FullName, "DungeonGeneration.json");
        string items = Path.Combine(root.FullName, "ItemTemplates.json");
        string npcs = Path.Combine(root.FullName, "NpcTemplates.json");
        try
        {
            File.WriteAllText(generation, Generation);
            File.WriteAllText(items, """{"FIND":{"Templates":[1]},"RETN":{"Templates":[2]},"TOOL":{"Templates":[3]},"FIXD":{"Templates":[4]}}""");
            File.WriteAllText(npcs, """{"BOSS":{"Children":["LEAF"]},"LEAF":{"Templates":[{"Level":1}]}}""");
            Require(!DoaRules.AuthoritativeReferenceIssues(root.FullName).Any());
            File.WriteAllText(npcs, """{"BOSS":{"Children":["BOSS"]}}""");
            Require(DoaRules.AuthoritativeReferenceIssues(root.FullName).Any(finding => finding.Rule == "DOA_UNRESOLVED_REFERENCE"));
        }
        finally
        {
            File.Delete(generation);
            File.Delete(items);
            File.Delete(npcs);
            root.Delete();
        }
    }
}
